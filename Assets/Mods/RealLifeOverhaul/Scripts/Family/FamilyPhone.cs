using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>The Family, Someone special (the default bridge's partner) and in-laws contacts, and what can be done from a call.</summary>
    public static partial class FamilyPhone
    {
        public const string FamilyId = "reallife-family";
        public const string PartnerId = "reallife-partner";
        public const string InLawId = "reallife-inlaws";

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(3);   // kept for the session: the game caches contact icons by sprite name

        private static readonly string[] Topics =
        {
            "reallife_family_topic_1", "reallife_family_topic_2", "reallife_family_topic_3", "reallife_family_topic_4", "reallife_family_topic_5", "reallife_family_topic_6"
        };

        private static int _lastTopic = -1;

        public static Contact FamilyContact { get; private set; }
        private static Contact _partnerContact;   // the stand-in contact, made only while Social does not keep the partner

        /// <summary>
        /// Who the partner's texts come from: Family's stand-in contact, or while Social keeps the partner their own contact in
        /// People, so that a text from them is a text in their thread and not a notice on screen.
        /// </summary>
        public static Contact PartnerContact => _partnerContact ?? (Partner.Kept(out _) ? FamilyBridges.Social.PartnerContact : null);
        public static Contact InLawContact { get; private set; }

        /// <summary>
        /// While the social life keeps the partner and the home's fridge can show the family's board, there is no Family contact:
        /// what it offered is face to face at home, on the fridge's Family button (Board), in the partner's People call (CallMatters:
        /// time apart and the in-laws) and, for a new home, a text to the partner (FamilySms). The family's own texts are notices then.
        /// </summary>
        public static bool Hides => Partner.UsesSocial && Settings.HomeLife && Settings.HomeRoutines;

        /// <summary>True while the fridge's board is open: nothing that needs the partner face to face is offered from it.</summary>
        internal static bool AtBoard;

        /// <summary>Whether the fridge's panel shows the Family button (Home/HomeFridgeButton, through Wiring/FamilyLinks).</summary>
        public static bool BoardShown => Settings.Family && FamilyState.Loaded && Hides;

        /// <summary>The fridge's Family button: the family's page, then everything that can be done from it.</summary>
        public static void Board()
        {
            AtBoard = true;
            FamilyTalk.Open(() => FamilyPageDialog.Guard(() => FamilyPage.Page(0)), () => AtBoard = false);
        }

        /// <summary>Whether the partner's People call offers "Family matters": time apart or the in-laws, with something to say.</summary>
        public static bool HasCallMatters
        {
            get
            {
                if (!Settings.Family || !FamilyState.Loaded || !Hides)
                {
                    return false;
                }
                Choices();
                return _callChoices.Exists(c => c.When == null || c.When());
            }
        }

        /// <summary>"Family matters" in the partner's People call.</summary>
        public static DialogEntry CallMatters()
        {
            Choices();
            return FamilyPageDialog.Guard(() => FamilyTalk.Menu(_callChoices, 0));
        }

        /// <summary>The family's badge: the Family contact's picture, and the icon of a wedding in the open on the city map.</summary>
        public static Sprite Icon => Disc(FamilyId, new Color(0.93f, 0.42f, 0.55f));

        public static void Setup()
        {
            FamilyContact = null;
            _partnerContact = null;
            InLawContact = null;
            if (!Settings.PhoneContacts || !Settings.Family)
            {
                return;
            }
            try
            {
                if (Hides)
                {
                    SaveGameManager.Current?.Contacts.RemoveAll(c => c != null && c.id == FamilyId);   // an earlier session's, made again when it is wanted
                }
                else
                {
                    FamilyContact = Register(FamilyId, "reallife_contact_family_desc", "reallife_family_calldialogtype", () => new FamilyPageDialog(),
                        "reallife_text_family_welcome");
                }
                if (!Partner.UsesSocial)
                {
                    _partnerContact = Register(PartnerId, "reallife_contact_partner_desc", "reallife_partner_calldialogtype", () => new PartnerDialog(),
                        "reallife_text_partner_welcome");
                }
                else if (SaveGameManager.Current != null && SaveGameManager.Current.Contacts.Exists(c => c != null && c.id == PartnerId))
                {
                    // The stand-in contact of an earlier session is not made again, but it still answers and says where the partner is now.
                    Register(PartnerId, "reallife_contact_partner_desc", "reallife_partner_calldialogtype", () => new PartnerDialog(),
                        "reallife_text_partner_welcome");
                }
                SetupInLaws();
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
                FamilyDirector.Logger?.Warn("Family: the phone contacts could not be added; family life goes on through notices and the options page.");
            }
        }

        public static void SetupInLaws()
        {
            if (InLawContact != null || !FamilyState.S.InLaws || !Settings.PhoneContacts || !Settings.FamilyRelatives)
            {
                return;
            }
            try
            {
                InLawContact = Register(InLawId, "reallife_contact_inlaws_desc", "reallife_inlaws_calldialogtype", () => new InLawDialog(),
                    "reallife_text_inlaws_welcome");
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        private static void Picture(string id)
        {
            if (id == PartnerId && Partner.Seed != 0)
            {
                ContactPictures.Person(PartnerId, Partner.Female, FamilyCast.PartnerAge(Partner.Seed), Partner.Seed);
                return;
            }
            ContactPictures.Add(id);
        }

        private static Contact Register(string id, string descriptionKey, string dialogTypeName, Func<Dialog> dialog, string welcomeKey)
        {
            Picture(id);
            Contact contact = Contact.GetContact(id, ContactCategoryName.General, descriptionKey, null, false, true);
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(dialogTypeName);
            contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
            CallDialogFactory.RegisterDialog(dialogType, RealLifeOverhaul.Guard.Maker(dialog));
            if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
            {
                contact.SendMessage(new TextMessage(welcomeKey), notify: false);
            }
            return contact;
        }

        private static Sprite Disc(string id, Color colour)
        {
            if (Icons.TryGetValue(id, out Sprite made) && made != null)
            {
                return made;
            }
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float radius = size / 2f - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    Color pixel = distance < radius * 0.42f ? Color.white : colour;
                    pixel.a = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = pixel;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = id;
            Icons[id] = sprite;
            return sprite;
        }

        /// <summary>Everything that can be done from a family conversation, from the entry at <paramref name="from"/> on (0 is the first).</summary>
        internal static DialogEntry FamilyMenu(int from)
        {
            return FamilyTalk.Menu(Choices(), from);
        }

        /// <summary>The same things, starting with what one says to the partner: on the phone a chat, face to face at home the evening's routines.</summary>
        internal static DialogEntry PartnerMenu(bool inPerson)
        {
            Choices();
            return FamilyTalk.Menu(_choices, inPerson ? _cookAt : _chatAt);
        }

        internal static DialogEntry PartnerFirst()
        {
            FamilySave s = FamilyState.S;
            if (!Settings.Family)
            {
                return FamilyTalk.Close("reallife_family_menu_off");
            }
            if (Partner.UsesSocial && (Partner.Kept(out _) || !FamilyState.HasPartner))
            {
                return FamilyTalk.Close(FamilyState.HasPartner ? "reallife_family_partner_in_people" : "reallife_family_partner_none_people");
            }
            if (FamilyState.Bond == Bond.Single || FamilyState.Bond == Bond.Divorced)
            {
                if (s.FreeDay > 0 && TimeHelper.CurrentDay - s.FreeDay < FamilyBalance.MeetAgainDays)
                {
                    return FamilyTalk.Close("reallife_family_meet_too_soon");
                }
                return FamilyTalk.Either(FamilyText.Say("reallife_family_meet_offer"), "reallife_family_meet_yes", () =>
                {
                    string name = Partner.Meet(Settings.FamilyInterest);
                    FamilyState.Remember("reallife_family_memory_met", name);
                    return FamilyTalk.Close("reallife_family_meet_done", Args.Us.With(FamilyText.T(Partner.JobKey)));
                }, "reallife_family_meet_no", () => FamilyTalk.Close("reallife_family_meet_later"));
            }
            return PartnerMenu(false);
        }

        private static DialogEntry Chat()
        {
            FamilySave s = FamilyState.S;
            s.LastTalkDay = TimeHelper.CurrentDay;
            s.LastChatDay = TimeHelper.CurrentDay;
            Partner.Tell(FamilyEvent.SharedTime, FamilyBalance.AffectionChat, 1f);
            FamilyState.Harmony(2f);
            ModState.AddModifier(ModState.Social);
            string topic = Topic(out Args fill);
            return FamilyTalk.Close(topic, fill);
        }

        // The first thing that is true today, most pressing first; otherwise small talk that never comes twice running.
        private static string Topic(out Args fill)
        {
            FamilySave s = FamilyState.S;
            fill = Args.Us;
            int day = TimeHelper.CurrentDay;
            if (ModState.IsSick)
            {
                return "reallife_family_topic_sick";
            }
            if (s.Wedding.Active && s.Wedding.Day - day <= 3)
            {
                fill = WeddingPlanner.PlanFill(s.Wedding);
                return "reallife_family_topic_wedding_near";
            }
            if (FamilyState.Process == ProcessKind.Pregnancy && s.PDueDay - day <= 5)
            {
                return "reallife_family_topic_due_soon";
            }
            if (JobDirector.EarnedToday > 0 && Random.value < 0.7f)
            {
                fill = Args.Us.Cost(JobDirector.EarnedToday);
                return "reallife_family_topic_shift";
            }
            ChildRecord small = FamilyState.Smallest();
            if (small != null && Random.value < 0.5f)
            {
                fill = Args.Us.Kid(small.Name);
                return "reallife_family_topic_baby";
            }
            if (SaveGameManager.Current.Happiness < 35f)
            {
                return "reallife_family_topic_low";
            }
            if (RainHelper.AreRainDropsFalling && Random.value < 0.6f)
            {
                return "reallife_family_topic_rain";
            }
            if (!string.IsNullOrEmpty(ModState.LastEventId) && Random.value < 0.4f)
            {
                return "reallife_family_topic_day";
            }
            fill = Args.Us.With(FamilyText.T(Partner.JobKey));
            return FamilyText.Pick(Topics, ref _lastTopic);
        }
    }

    /// <summary>The Family contact's call: the Family page.</summary>
    public sealed class FamilyPageDialog : Dialog
    {
        public FamilyPageDialog()
        {
            npcNameKey = FamilyPhone.FamilyId;
            DialogController.current.ShowEntry(RealLifeOverhaul.Guard.Entry(Guard(() => FamilyPage.Page(0))));
        }

        /// <summary>A call's first page, or a polite close when family life is not loaded or the page faults.</summary>
        internal static DialogEntry Guard(Func<DialogEntry> first)
        {
            try
            {
                if (!FamilyState.Loaded || FamilyDirector.Instance == null)
                {
                    return FamilyTalk.Close("reallife_family_menu_off");
                }
                return first();
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
                return FamilyTalk.Close("reallife_family_no_fault");
            }
        }
    }

    /// <summary>The "Someone special" contact's call: meeting somebody, or what can be said to the partner.</summary>
    public sealed class PartnerDialog : Dialog
    {
        public PartnerDialog()
        {
            npcNameKey = FamilyPhone.PartnerId;
            DialogController.current.ShowEntry(RealLifeOverhaul.Guard.Entry(FamilyPageDialog.Guard(FamilyPhone.PartnerFirst)));
        }
    }

    /// <summary>The in-laws' call: a chat or the favour they asked for, and their advice.</summary>
    public sealed class InLawDialog : Dialog
    {
        public InLawDialog()
        {
            npcNameKey = FamilyPhone.InLawId;
            DialogController.current.ShowEntry(RealLifeOverhaul.Guard.Entry(FamilyPageDialog.Guard(() =>
                !FamilyState.S.InLaws || !FamilyState.HasPartner
                    ? FamilyTalk.Close("reallife_family_inlaws_gone")
                    : FamilyTalk.Either(FamilyText.Say(Relatives.InLawFavourOpen ? "reallife_family_inlaws_hello_favour" : "reallife_family_inlaws_hello"),
                        Relatives.InLawFavourOpen ? "reallife_family_inlaws_help" : "reallife_family_inlaws_chat",
                        Relatives.InLawFavourOpen ? (Func<DialogEntry>)Relatives.InLawFavour : Relatives.InLawChat,
                        "reallife_family_inlaws_advice", Relatives.InLawAdvice))));
        }
    }
}
