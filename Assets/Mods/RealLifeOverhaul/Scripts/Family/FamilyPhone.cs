using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using Helpers;
using Streets;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Family life on the phone, registered the way the mod's other contacts are
    /// (Phone/PhoneContacts): "Family" is the Family page (household, each member, what is
    /// coming up, the memories, the budget) and everything that can be decided from a phone;
    /// "Someone special" is the partner the default bridge keeps (it steps aside when Social
    /// keeps them instead); the in-laws get a contact of their own with the engagement.
    /// </summary>
    public static class FamilyPhone
    {
        public const string FamilyId = "reallife-family";
        public const string PartnerId = "reallife-partner";
        public const string InLawId = "reallife-inlaws";

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>(3);   // kept for the session: the game caches contact icons by sprite name
        private static List<Choice> _choices;
        private static int _chatAt, _cookAt;

        private static readonly string[] Topics =
        {
            "reallife_family_topic_1", "reallife_family_topic_2", "reallife_family_topic_3", "reallife_family_topic_4", "reallife_family_topic_5", "reallife_family_topic_6"
        };

        private static int _lastTopic = -1;

        public static Contact FamilyContact { get; private set; }
        public static Contact PartnerContact { get; private set; }
        public static Contact InLawContact { get; private set; }

        /// <summary>The family's badge: the Family contact's picture, and the icon of a wedding in the open on the city map.</summary>
        public static Sprite Icon => Disc(FamilyId, new Color(0.93f, 0.42f, 0.55f));

        public static void Setup()
        {
            FamilyContact = null;
            PartnerContact = null;
            InLawContact = null;
            if (!Settings.PhoneContacts || !Settings.Family)
            {
                return;
            }
            try
            {
                FamilyContact = Register(FamilyId, "reallife_contact_family_desc", "reallife_family_calldialogtype", () => new FamilyPageDialog(),
                    "reallife_text_family_welcome");
                if (!Partner.UsesSocial)
                {
                    PartnerContact = Register(PartnerId, "reallife_contact_partner_desc", "reallife_partner_calldialogtype", () => new PartnerDialog(),
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

        /// <summary>The in-laws' contact, once there are in-laws.</summary>
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

        // A contact's picture from the game's own assets (Phone/Contacts): the partner's own face, as the family dresses them in
        // the city (FamilyCast.Dress); the family and the in-laws from the contact catalogue.
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
            CallDialogFactory.RegisterDialog(dialogType, dialog);
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

        // ---------------------------------------------------------------- what can be done

        /// <summary>Everything that can be done from a family conversation, starting with the entry at <paramref name="from"/> (0 is the first; the last is the way back to the page).</summary>
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

        private static List<Choice> Choices()
        {
            if (_choices != null)
            {
                return _choices;
            }
            var list = new List<Choice>(40);
            Func<Bond> bond = () => FamilyState.Bond;
            Func<FamilySave> s = () => FamilyState.S;

            // ---- the two of you
            _chatAt = list.Count;
            list.Add(new Choice(() => !Partner.UsesSocial && !FamilyState.HasPartner && Settings.Family,
                () => FamilyText.Say("reallife_family_do_meet"), "reallife_family_do_meet_yes", PartnerFirst));
            list.Add(new Choice(() => FamilyState.Together && (!Partner.UsesSocial || InPerson) && s().LastChatDay != TimeHelper.CurrentDay,
                () => FamilyText.Say("reallife_family_do_chat"), "reallife_family_do_chat_yes", Chat));
            list.Add(new Choice(() => HomeLife.CanPlanDate, () => FamilyText.Say("reallife_family_do_date", Args.Us.Cost(FamilyBalance.DinnerCost)), "reallife_family_do_date_yes", HomeLife.PlanDate));
            _cookAt = list.Count;
            list.Add(new Choice(() => HomeLife.CanCook && InPerson, () => FamilyText.Say("reallife_family_do_cook"), "reallife_family_do_cook_yes", HomeLife.Cook));
            list.Add(new Choice(() => HomeLife.CanWatch && InPerson, () => FamilyText.Say("reallife_family_do_watch"), "reallife_family_do_watch_yes", HomeLife.Watch));
            list.Add(new Choice(() => FamilyState.Together && !s().LivesIn,
                () => FamilyText.Say("reallife_family_do_move_in"), "reallife_family_do_move_in_yes", () =>
                {
                    string why = HomeLife.MoveIn(false);
                    return FamilyTalk.Close(why ?? "reallife_family_move_in_yes");
                }));
            list.Add(new Choice(() => FamilyState.Together && s().LivesIn && BuildingManager.IsInsideBuilding && !FamilyPlaces.AtHome() && InOwnHome(),
                () => FamilyText.Say("reallife_family_do_new_home"), "reallife_family_do_new_home_yes", () =>
                {
                    BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
                    if (!FamilyPlaces.HasRoom(here, 0))
                    {
                        return FamilyTalk.Close("reallife_family_no_room");
                    }
                    FamilyPlaces.MakeHome(here);
                    return FamilyTalk.Close("reallife_family_new_home_done");
                }));

            // ---- the ring and the question
            list.Add(new Choice(() => bond() == Bond.Dating && s().Ring == 0 && Courtship.Ready() && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_jeweller"), "reallife_family_do_show_way", () => Way(FamilyPlaces.Jeweller, "reallife_family_way_jeweller", () => Courtship.Rings(0))));
            list.Add(new Choice(() => bond() == Bond.Dating && s().Ring > 0 && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_propose", Args.Us.With(FamilyText.T(Courtship.RingName(s().Ring)))), "reallife_family_do_propose_yes", () =>
                {
                    FamilyDirector director = FamilyDirector.Instance;
                    director.WhenFree(() =>
                    {
                        TestRefusal why = director.SceneRefusal(false);
                        string refusal = why != TestRefusal.None ? "reallife_family_no_busy" : null;
                        if (refusal == null)
                        {
                            director.StartScene(new ProposalScene(false), out refusal);
                        }
                        if (refusal != null)
                        {
                            FamilyText.Toast(UI.Notification.NotificationType.Warning, refusal, Args.Us);
                        }
                    });
                    return FamilyTalk.Close("reallife_family_propose_called");
                }));

            // ---- the wedding
            list.Add(new Choice(() => bond() == Bond.Engaged && !s().Wedding.Active && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_plan"), "reallife_family_do_plan_yes", () => WeddingPlanner.Start(false)));
            list.Add(new Choice(() => bond() == Bond.Engaged && !s().Wedding.Active && Settings.FamilyWedding,
                () => FamilyText.Say("reallife_family_do_planner"), "reallife_family_do_show_way", () => Way(FamilyPlaces.Planner, "reallife_family_way_planner")));
            list.Add(new Choice(() => WeddingPlanner.CanInvite, () => FamilyText.Say("reallife_family_do_invite"), "reallife_family_do_invite_yes", WeddingPlanner.Invite));
            list.Add(new Choice(() => s().Wedding.Active && FamilyState.FindErrand(Errands.Flowers) != null,
                () => FamilyText.Say("reallife_family_do_florist"), "reallife_family_do_show_way", () => Way(FamilyPlaces.Florist, "reallife_family_way_florist", () =>
                {
                    // No florist in the city: ordered by telephone instead.
                    Errand errand = FamilyState.FindErrand(Errands.Flowers);
                    if (errand != null)
                    {
                        WeddingPlanner.OrderFlowers(errand);
                    }
                    return FamilyTalk.Close("reallife_family_way_florist_phone");
                })));
            list.Add(new Choice(() => s().Wedding.Active, () => FamilyText.Say("reallife_family_do_postpone", WeddingPlanner.PlanFill(s().Wedding)), "reallife_family_do_postpone_yes", WeddingPlanner.Postpone));
            list.Add(new Choice(() => s().Wedding.Active, () => FamilyText.Say("reallife_family_do_cancel", WeddingPlanner.PlanFill(s().Wedding)), "reallife_family_do_cancel_yes", WeddingPlanner.CancelAsk));
            list.Add(new Choice(() => bond() == Bond.Engaged, () => FamilyText.Say("reallife_family_do_break_off"), "reallife_family_do_break_off_yes",
                () => FamilyTalk.Either(FamilyText.Say("reallife_family_break_off_ask"), "reallife_family_break_off_sure", () =>
                {
                    Courtship.CallOff();
                    return FamilyTalk.Close("reallife_family_break_off_done");
                }, "reallife_family_apart_no", () => FamilyTalk.Close("reallife_family_apart_stayed"))));
            list.Add(new Choice(() => WeddingDay.HoneymoonOpen, () => FamilyText.Say("reallife_family_do_honeymoon", Args.Us.Cost(FamilyBalance.HoneymoonCost)), "reallife_family_do_honeymoon_yes", () =>
            {
                FamilyDirector.Instance.WhenFree(WeddingDay.OfferHoneymoon);
                return FamilyTalk.Close("reallife_family_honeymoon_asking");
            }));

            // ---- children
            list.Add(new Choice(() => Children.CanTalk, () => FamilyText.Say("reallife_family_do_children"), "reallife_family_do_children_yes", Children.Talk));
            list.Add(new Choice(() => FamilyState.AnyUnder(FamilyBalance.SchoolFrom),
                () => FamilyText.Say("reallife_family_do_care", Args.Us.With(FamilyText.T(Children.CareName(FamilyState.Care)))), "reallife_family_do_care_yes",
                () => Children.CareStep(s().CareChoice + 1)));
            list.Add(new Choice(() => FamilyState.Smallest() != null && s().Pushchair,
                () => FamilyText.Say("reallife_family_do_walk", Args.Us.Kid(FamilyState.Smallest().Name)), "reallife_family_do_walk_yes", () =>
                {
                    FamilyDirector director = FamilyDirector.Instance;
                    director.WhenFree(() =>
                    {
                        TestRefusal why = director.SceneRefusal(true);
                        string refusal = why == TestRefusal.Indoors ? "reallife_family_no_walk_indoors" : why != TestRefusal.None ? "reallife_family_no_busy" : null;
                        if (refusal == null)
                        {
                            director.StartScene(new WalkScene(false), out refusal);
                        }
                        if (refusal != null)
                        {
                            FamilyText.Toast(UI.Notification.NotificationType.Warning, refusal, Args.Us);
                        }
                    });
                    return FamilyTalk.Close("reallife_family_walk_called");
                }));
            for (int i = 0; i < FamilyBalance.MaxChildren; i++)
            {
                int child = i;
                list.Add(new Choice(() => Has(child) && s().Children[child].Age >= FamilyBalance.PocketFrom,
                    () => FamilyText.Say("reallife_family_do_pocket", Args.Us.Kid(s().Children[child].Name).Cost(FamilyBalance.PocketMoney[Mathf.Clamp(s().Children[child].Pocket, 0, 3)])),
                    "reallife_family_pocket_change", () => Children.PocketStep(child)));
                list.Add(new Choice(() => Has(child),
                    () => FamilyText.Say("reallife_family_do_fund", Args.Us.Kid(s().Children[child].Name).Cost(s().Children[child].Fund).With(FamilyText.Money(FamilyBalance.FundStep))),
                    "reallife_family_do_fund_yes", () => Children.FundStep(child)));
            }
            list.Add(new Choice(() => ShoppingErrand() != null, () => FamilyText.Say("reallife_family_do_shopping"), "reallife_family_do_show_way", () =>
            {
                Errand errand = ShoppingErrand();
                return errand != null ? Way(Errands.PlacesOf(errand.Kind), "reallife_family_way_shop") : FamilyTalk.Close("reallife_family_menu_nothing");
            }));

            // ---- relatives
            list.Add(new Choice(() => Relatives.CanGoToMoms, () => FamilyText.Say("reallife_family_do_moms"), "reallife_family_do_moms_yes", Relatives.GoToMoms));
            list.Add(new Choice(() => Relatives.InLawFavourOpen, () => FamilyText.Say("reallife_family_do_favour"), "reallife_family_do_favour_yes", Relatives.InLawFavour));
            list.Add(new Choice(null, () => FamilyText.Say("reallife_family_do_kin", Args.Us.With(Relatives.NextOfKin())), "reallife_family_kin_change", Relatives.NextOfKinStep));

            // ---- hard times
            list.Add(new Choice(() => HardTimes.CanCounsel && Settings.FamilyFriction, () => FamilyText.Say("reallife_family_do_counselling", Args.Us.Cost(FamilyBalance.CounsellingCost)), "reallife_family_do_counselling_yes", HardTimes.Book));
            list.Add(new Choice(() => HardTimes.CanCallApart, () => FamilyText.Say("reallife_family_do_call_apart"), "reallife_family_do_chat_yes", HardTimes.CallApart));
            list.Add(new Choice(() => HardTimes.CanComeHome, () => FamilyText.Say("reallife_family_do_come_home"), "reallife_family_do_come_home_yes", HardTimes.ComeHome));
            list.Add(new Choice(() => FamilyState.Together && s().Harmony < 50f && Settings.FamilyFriction, () => FamilyText.Say("reallife_family_do_apart"), "reallife_family_do_apart_yes", HardTimes.AskApart));
            list.Add(new Choice(() => HardTimes.CanEnd, () => FamilyText.Say((Bond)s().BondBefore == Bond.Married ? "reallife_family_do_divorce" : "reallife_family_do_part"), "reallife_family_do_end_yes", HardTimes.EndAsk));

            // ---- and back to the page
            list.Add(new Choice(null, () => FamilyText.Say("reallife_family_do_page"), "reallife_family_do_page_yes", () => FamilyPage.Page(0)));
            _choices = list;
            return _choices;
        }

        private static bool InPerson => DialogController.current != null && DialogController.current.dialogType != DialogType.PhoneCall && HomeLife.Present;

        private static bool Has(int child)
        {
            return child < FamilyState.S.Children.Count && !FamilyState.S.Children[child].Grown;
        }

        private static bool InOwnHome()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.building != null && manager.IsPlayerOwnedBusiness && manager.building.BuildingType == FamilyPlaces.Residential;
        }

        private static Errand ShoppingErrand()
        {
            FamilySave s = FamilyState.S;
            for (int i = 0; i < s.Errands.Count; i++)
            {
                if (s.Errands[i].Day <= 0 && Errands.PlacesOf(s.Errands[i].Kind) != null && s.Errands[i].Kind != Errands.Flowers)
                {
                    return s.Errands[i];
                }
            }
            return null;
        }

        // The nearest of a kind of place, on the game's own destination marker.
        private static DialogEntry Way(string[] kinds, string key, Func<DialogEntry> without = null)
        {
            if (kinds == null || !FamilyPlaces.Nearest(kinds, PlayerHelper.GetCityPosition(), out Venue venue))
            {
                // The city has none: whatever can be done by telephone instead is, so nothing depends on a shop that is not there.
                return without != null ? without() : FamilyTalk.Close("reallife_family_way_none");
            }
            FamilyPlaces.Guide(venue);
            return FamilyTalk.Close(key, Args.Us.At(venue.Name));
        }

        // A chat about what has really been going on.
        private static DialogEntry Chat()
        {
            FamilySave s = FamilyState.S;
            s.LastTalkDay = TimeHelper.CurrentDay;
            s.LastChatDay = TimeHelper.CurrentDay;
            Partner.Tell(FamilyEvent.SharedTime, FamilyBalance.AffectionChat, 1f);
            FamilyState.Harmony(2f);
            ModState.AddModifier(ModState.Social);
            return FamilyTalk.Close(Topic(), TopicFill());
        }

        private static Args _topicFill;

        private static Args TopicFill()
        {
            return _topicFill;
        }

        private static string Topic()
        {
            FamilySave s = FamilyState.S;
            _topicFill = Args.Us;
            int day = TimeHelper.CurrentDay;
            // The first thing that is true today, most pressing first; otherwise small talk that never comes twice running.
            if (ModState.IsSick)
            {
                return "reallife_family_topic_sick";
            }
            if (s.Wedding.Active && s.Wedding.Day - day <= 3)
            {
                _topicFill = WeddingPlanner.PlanFill(s.Wedding);
                return "reallife_family_topic_wedding_near";
            }
            if (FamilyState.Process == ProcessKind.Pregnancy && s.PDueDay - day <= 5)
            {
                return "reallife_family_topic_due_soon";
            }
            if (JobDirector.EarnedToday > 0 && Random.value < 0.7f)
            {
                _topicFill = Args.Us.Cost(JobDirector.EarnedToday);
                return "reallife_family_topic_shift";
            }
            ChildRecord small = FamilyState.Smallest();
            if (small != null && Random.value < 0.5f)
            {
                _topicFill = Args.Us.Kid(small.Name);
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
            _topicFill = Args.Us.With(FamilyText.T(Partner.JobKey));
            return FamilyText.Pick(Topics, ref _lastTopic);
        }
    }

    /// <summary>The Family page: five short pages read out one after another, and from any of them the things to do.</summary>
    internal static class FamilyPage
    {
        private const int Pages = 5;

        public static DialogEntry Page(int index)
        {
            int page = (index % Pages + Pages) % Pages;
            if (!Settings.Family)
            {
                return FamilyTalk.Close("reallife_family_menu_off");
            }
            string text;
            switch (page)
            {
                case 0: text = Household(); break;
                case 1: text = Members(); break;
                case 2: text = Dates(); break;
                case 3: text = Memories(); break;
                default: text = Budget(); break;
            }
            return FamilyTalk.Either(FamilyText.Built(text), "reallife_family_page_next", () => Page(page + 1), "reallife_family_page_do", () => FamilyPhone.FamilyMenu(0));
        }

        private static string Household()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(300);
            text.Append(FamilyText.T("reallife_family_page_household"));
            if (!FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T(FamilyState.Bond == Bond.Divorced ? "reallife_family_page_divorced" : "reallife_family_page_single", new Args { Name = s.ExName }));
            }
            else
            {
                int days = Mathf.Max(0, TimeHelper.CurrentDay - (s.SinceDay > 0 ? s.SinceDay : s.PMetDay));
                text.Append('\n').Append(FamilyText.T("reallife_family_page_partner", Args.Us.With(FamilyText.T(FamilyText.BondWord(FamilyState.Bond))).Count(days)));
                text.Append('\n').Append(FamilyText.T("reallife_family_page_harmony", Args.Us.With(FamilyText.T(FamilyText.HarmonyWord(s.Harmony))).Count(Mathf.RoundToInt(s.Harmony))));
                if (s.LivesIn && FamilyState.Together)
                {
                    string where;
                    switch (HomeLife.Where(TimeHelper.CurrentDay, TimeHelper.CurrentHour))
                    {
                        case Whereabouts.Work: where = "reallife_family_page_where_work"; break;
                        case Whereabouts.Out: where = "reallife_family_page_where_out"; break;
                        case Whereabouts.Asleep: where = "reallife_family_page_where_asleep"; break;
                        default: where = "reallife_family_page_where_home"; break;
                    }
                    text.Append('\n').Append(FamilyText.T(where, Args.Us));
                }
                if (s.Ring > 0 && FamilyState.Bond == Bond.Dating)
                {
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_ring", Args.Us.With(FamilyText.T(Courtship.RingName(s.Ring)))));
                }
            }
            BuildingRegistration home = FamilyPlaces.Home();
            if (home != null)
            {
                int sqm = FamilyPlaces.Sqm(home);
                text.Append('\n').Append(FamilyText.T(sqm > 0 ? "reallife_family_page_home" : "reallife_family_page_home_plain", new Args { Place = home.Address.ToFormattedString(), N = sqm.ToString() }));
            }
            else
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_no_home"));
            }
            text.Append('\n').Append(FamilyText.T("reallife_family_page_children", new Args { N = FamilyState.AtHome().ToString() }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_kin", new Args { Text = Relatives.NextOfKin() }));
            return text.ToString();
        }

        private static string Members()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_members"));
            if (FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_partner", Args.Us.With(FamilyText.T(Partner.JobKey)).Cost(Partner.Income)));
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                string key = c.Grown ? "reallife_family_page_member_grown" : c.School == 2 ? "reallife_family_page_member_private" : c.School == 1 ? "reallife_family_page_member_school"
                    : c.Age < 1f ? "reallife_family_page_member_baby" : "reallife_family_page_member_small";
                string close = c.Closeness >= 75f ? "reallife_family_close_very" : c.Closeness >= 50f ? "reallife_family_close_close" : c.Closeness >= 30f ? "reallife_family_close_distant" : "reallife_family_close_far";
                text.Append('\n').Append(FamilyText.T(key, new Args { Child = c.Name, Text = FamilyText.Age(c.Age), Name = FamilyText.T(close), Amount = FamilyText.Money(c.Fund) }));
            }
            if (PhoneContacts.Mom != null)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_mom"));
            }
            if (s.InLaws && FamilyState.HasPartner)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_member_inlaws", Args.Us));
            }
            if (s.Children.Count == 0 && !FamilyState.HasPartner && PhoneContacts.Mom == null)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_nobody"));
            }
            return text.ToString();
        }

        private static string Dates()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_dates"));
            int lines = 0;
            if (s.Wedding.Active)
            {
                text.Append('\n').Append(FamilyText.T(s.Wedding.Invited ? "reallife_family_page_wedding" : "reallife_family_page_wedding_uninvited", WeddingPlanner.PlanFill(s.Wedding)));
                lines++;
            }
            if (FamilyState.Process == ProcessKind.Pregnancy)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_due", Args.Us.On(FamilyText.When(s.PDueDay))));
                lines++;
            }
            else if (FamilyState.Process == ProcessKind.Adoption)
            {
                text.Append('\n').Append(FamilyText.T(s.PStep == 0 ? "reallife_family_page_adopt_file" : "reallife_family_page_adopt_wait", Args.Us));
                lines++;
            }
            string errands = Errands.Upcoming();
            if (errands.Length > 0)
            {
                text.Append('\n').Append(errands);
                lines++;
            }
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (!c.Grown)
                {
                    int days = Mathf.CeilToInt((Mathf.Floor(c.Age) + 1f - c.Age) * Children.DaysPerYear);
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_birthday", new Args { Child = c.Name, Day = FamilyText.When(TimeHelper.CurrentDay + days), N = (Mathf.FloorToInt(c.Age) + 1).ToString() }));
                    lines++;
                }
            }
            // The next family dinner on the game's calendar.
            int year = Mathf.Max(8, SaveGameManager.Current.gameVariables.daysPerYear);
            int dayOfYear = (TimeHelper.CurrentDay - 1) % year;
            for (int k = 0; k < 5; k++)
            {
                int which = k % 4;
                int on = Mathf.Clamp(Mathf.RoundToInt(year * (which == 0 ? 0.22f : which == 1 ? 0.47f : which == 2 ? 0.72f : 0.97f)), 1, year - 1) + (k / 4) * year;
                if (on >= dayOfYear)
                {
                    text.Append('\n').Append(FamilyText.T("reallife_family_page_holiday", new Args { Text = FamilyText.T(Relatives.HolidayName(which)), Day = FamilyText.When(TimeHelper.CurrentDay + on - dayOfYear) }));
                    lines++;
                    break;
                }
            }
            if (lines == 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_nothing_due"));
            }
            return text.ToString();
        }

        private static string Memories()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(500);
            text.Append(FamilyText.T("reallife_family_page_memories"));
            if (s.Memories.Count == 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_no_memories"));
                return text.ToString();
            }
            for (int i = s.Memories.Count - 1; i >= 0 && i >= s.Memories.Count - 8; i--)
            {
                Memory m = s.Memories[i];
                string line = FamilyText.T(m.Key, new Args { Name = m.Who, Child = m.Who, Text = m.Who });
                text.Append('\n').Append(FamilyText.T("reallife_family_page_memory_line", new Args { N = m.Day.ToString(), Text = line }));
            }
            return text.ToString();
        }

        private static string Budget()
        {
            FamilySave s = FamilyState.S;
            var text = new StringBuilder(400);
            text.Append(FamilyText.T("reallife_family_page_budget"));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_in", new Args { Amount = FamilyText.Money(s.MIn) }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_out", new Args
            {
                Amount = FamilyText.Money(s.MChildcare), Text = FamilyText.Money(s.MSchool), N = FamilyText.Money(s.MPocket), Place = FamilyText.Money(s.MFund), Day = FamilyText.Money(s.MOther)
            }));
            text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_all", new Args { Amount = FamilyText.Money(s.TIn), Text = FamilyText.Money(s.TOut) }));
            int funds = 0;
            for (int i = 0; i < s.Children.Count; i++)
            {
                funds += s.Children[i].Grown ? 0 : s.Children[i].Fund;
            }
            if (funds > 0)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_funds", new Args { Amount = FamilyText.Money(funds) }));
            }
            if (FamilyState.Together && s.LivesIn)
            {
                text.Append('\n').Append(FamilyText.T("reallife_family_page_budget_share", Args.Us));
            }
            return text.ToString();
        }
    }

    public class FamilyPageDialog : Dialog
    {
        public FamilyPageDialog()
        {
            npcNameKey = FamilyPhone.FamilyId;
            DialogController.current.ShowEntry(Guard(() => FamilyPage.Page(0)));
        }

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

    public class PartnerDialog : Dialog
    {
        public PartnerDialog()
        {
            npcNameKey = FamilyPhone.PartnerId;
            DialogController.current.ShowEntry(FamilyPageDialog.Guard(FamilyPhone.PartnerFirst));
        }
    }

    public class InLawDialog : Dialog
    {
        public InLawDialog()
        {
            npcNameKey = FamilyPhone.InLawId;
            DialogController.current.ShowEntry(FamilyPageDialog.Guard(() =>
                !FamilyState.S.InLaws || !FamilyState.HasPartner
                    ? FamilyTalk.Close("reallife_family_inlaws_gone")
                    : FamilyTalk.Either(FamilyText.Say(Relatives.InLawFavourOpen ? "reallife_family_inlaws_hello_favour" : "reallife_family_inlaws_hello"),
                        Relatives.InLawFavourOpen ? "reallife_family_inlaws_help" : "reallife_family_inlaws_chat", Relatives.InLawFavourOpen ? (Func<DialogEntry>)Relatives.InLawFavour : Relatives.InLawChat,
                        "reallife_family_inlaws_advice", Relatives.InLawAdvice)));
        }
    }
}
