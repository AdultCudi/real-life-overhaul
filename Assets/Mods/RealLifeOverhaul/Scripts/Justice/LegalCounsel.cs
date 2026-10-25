using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using Buildings;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using Streets;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One firm that can be retained.</summary>
    public sealed class LawFirm
    {
        public string Name;
        public string Street;
        public int Number;
        public int Stars;       // 1 to 5
        public bool Own;        // the player's own firm: no fee
        public bool Chambers;   // rooms in an office building, in a city with no law firm

        public string Address => new Address(Street, Number).ToFormattedString();
    }

    /// <summary>Who can represent the player and what they cost: the game's law firm businesses, or chambers in a city with none.</summary>
    public static class LegalCounsel
    {
        public const string LawFirmType = "ba:businesstype_lawfirm";
        private const string LawyerSkill = "ba:skill_lawyer";
        private const int MostFirms = 6;

        private static readonly string[] ChamberNames =
        {
            "reallife_justice_chambers_1", "reallife_justice_chambers_2", "reallife_justice_chambers_3"
        };

        private static readonly List<LawFirm> Firms = new List<LawFirm>(MostFirms);

        /// <summary>The firms there are, best first. Rebuilt on every call, when a conversation opens.</summary>
        public static List<LawFirm> List()
        {
            Firms.Clear();
            var save = SaveGameManager.Current;
            if (save == null || save.BuildingRegistrations == null)
            {
                return Firms;
            }
            foreach (BuildingRegistration registration in save.BuildingRegistrations)
            {
                if (registration == null || registration.businessTypeName != LawFirmType || string.IsNullOrEmpty(registration.StreetName) || !GameRegistrations.Found(registration))
                {
                    continue;
                }
                Add(From(registration));
            }
            if (Firms.Count == 0)
            {
                Chambers();
            }
            return Firms;
        }

        public static LawFirm From(BuildingRegistration registration)
        {
            // The game keeps the satisfaction of the businesses it runs at 50: the address fixes the rating and satisfaction moves it.
            int stars = 2 + JusticeLaw.Hash(registration.StreetName + "#" + registration.StreetNumber) % 4;
            if (registration.satisfaction != null)
            {
                stars += Mathf.RoundToInt((registration.satisfaction.overall - 50) / 25f);
            }
            return new LawFirm
            {
                Name = string.IsNullOrEmpty(registration.BusinessName) ? JusticeText.T("reallife_justice_firm_unnamed") : registration.BusinessName,
                Street = registration.StreetName, Number = registration.StreetNumber, Stars = Mathf.Clamp(stars, 1, 5), Own = GoingPublic.Mine(registration)
            };
        }

        // Kept sorted as it is built: best first, then by name. Never more than six.
        private static void Add(LawFirm firm)
        {
            int at = 0;
            while (at < Firms.Count && (Firms[at].Stars > firm.Stars
                                        || (Firms[at].Stars == firm.Stars && string.CompareOrdinal(Firms[at].Name, firm.Name) <= 0)))
            {
                at++;
            }
            if (at < MostFirms)
            {
                Firms.Insert(at, firm);
                if (Firms.Count > MostFirms)
                {
                    Firms.RemoveAt(Firms.Count - 1);
                }
            }
        }

        // The office buildings whose addresses hash lowest.
        private static void Chambers()
        {
            List<Building> all = BuildingHelper.allBuildings;
            if (all == null)
            {
                return;
            }
            for (int n = 0; n < ChamberNames.Length; n++)
            {
                Building best = null;
                int bestHash = int.MaxValue;
                for (int i = 0; i < all.Count; i++)
                {
                    Building building = all[i];
                    if (building == null || building.BuildingType != "ba:buildingtype_office" || Offices.Conversions.Holds(building)
                        || string.IsNullOrEmpty(building.StreetName) || Taken(building))
                    {
                        continue;
                    }
                    int hash = JusticeLaw.Hash(building.StreetName + "#" + building.StreetNumber);
                    if (hash < bestHash)
                    {
                        bestHash = hash;
                        best = building;
                    }
                }
                if (best == null)
                {
                    return;
                }
                Firms.Add(new LawFirm
                {
                    Name = JusticeText.T(ChamberNames[n]), Street = best.StreetName, Number = best.StreetNumber,
                    Stars = 2 + bestHash % 4, Chambers = true
                });
            }
            Firms.Sort((a, b) => b.Stars != a.Stars ? b.Stars.CompareTo(a.Stars) : string.CompareOrdinal(a.Name, b.Name));
        }

        private static bool Taken(Building building)
        {
            for (int i = 0; i < Firms.Count; i++)
            {
                if (Firms[i].Street == building.StreetName && Firms[i].Number == building.StreetNumber)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The player's own skill in law, 0 to 100.</summary>
        public static float LawyerSkillValue()
        {
            try
            {
                EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
                if (self != null && self.characterData != null && self.characterData.skills != null)
                {
                    foreach (Skill skill in self.characterData.skills)
                    {
                        if (skill != null && skill.name == LawyerSkill)
                        {
                            return skill.value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // no skill to be read: untrained
            }
            return 0f;
        }

        /// <summary>How good the player's representation is, 0 to 1; a case prepared face to face adds 0.05.</summary>
        public static float Quality(JusticeCase c)
        {
            float quality;
            switch (JusticeState.Lawyer)
            {
                case LawyerKind.PublicDefender: quality = JusticeLaw.PublicDefenderQuality; break;
                case LawyerKind.Firm: quality = JusticeLaw.FirmQuality(JusticeState.LawyerStars); break;
                case LawyerKind.Self: quality = JusticeLaw.SelfQuality(LawyerSkillValue()); break;
                default: quality = JusticeLaw.NoCounselQuality; break;
            }
            return Mathf.Clamp01(quality + (c != null && c.MetInPerson ? 0.05f : 0f));
        }

        /// <summary>Who represents the player, as shown.</summary>
        public static string Name()
        {
            switch (JusticeState.Lawyer)
            {
                case LawyerKind.PublicDefender: return JusticeText.T("reallife_justice_lawyer_pd");
                case LawyerKind.Firm: return JusticeState.LawyerName;
                case LawyerKind.Self: return JusticeText.T("reallife_justice_lawyer_self");
                default: return JusticeText.T("reallife_justice_lawyer_none");
            }
        }

        /// <summary>A firm's fee for the player's open cases, by the gravest of them and its rating, bounded against cash.</summary>
        public static int Fee(LawFirm firm)
        {
            if (firm == null || firm.Own)
            {
                return 0;
            }
            ChargeClass weight = ChargeClass.Misdemeanour;
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                if (JusticeState.Cases[i].Open && JusticeState.Cases[i].Weight > weight)
                {
                    weight = JusticeState.Cases[i].Weight;
                }
            }
            int fame = 0;
            try
            {
                fame = Justice.Bridges.Fame != null ? Mathf.Clamp(Justice.Bridges.Fame(), 0, 250) : 0;   // fame costs a little more, a quarter at most
            }
            catch (Exception)
            {
                // a bridge that throws adds nothing
            }
            return JusticeLaw.Bound(JusticeLaw.FirmFee(weight, firm.Stars) * (1000 + fame) / 1000, Crime.Cash, JusticeLaw.FeeShareOfCash, Prices.At(200));
        }

        /// <summary>Retains a firm: a quarter of the fee now if it can be afforded, the rest (or all of it) over six days.</summary>
        public static void Retain(LawFirm firm, IModLogger logger)
        {
            int fee = Fee(firm);
            int down = JusticeLaw.Round10(fee * 0.25f);
            if (down > 0 && Crime.Cash - down >= JusticeLaw.CashFloor && Crime.Pay(down, "reallife_transaction_legal_fee"))
            {
                fee -= down;
            }
            else
            {
                down = 0;
            }
            JusticeState.AddDebt(JusticeDebt.Fee, fee, JusticeLaw.FeePlanDays, 0);
            JusticeState.Lawyer = LawyerKind.Firm;
            JusticeState.LawyerName = firm.Name;
            JusticeState.LawyerStreet = firm.Street;
            JusticeState.LawyerNumber = firm.Number;
            JusticeState.LawyerStars = firm.Stars;
            logger?.Info("Justice: retained " + firm.Name + " (" + firm.Stars + "/5), " + down + " down, " + fee + " over " + JusticeLaw.FeePlanDays + " days.");
        }

        public static void RetainSimple(LawyerKind kind, IModLogger logger)
        {
            JusticeState.ClearLawyer();
            JusticeState.Lawyer = kind;
            logger?.Info("Justice: representation is now " + kind + ".");
        }

        public static bool IsRetained(BuildingRegistration registration)
        {
            return registration != null && JusticeState.Lawyer == LawyerKind.Firm && registration.StreetName == JusticeState.LawyerStreet
                   && registration.StreetNumber == JusticeState.LawyerNumber;
        }

        /// <summary>The one motion to dismiss: every charge under the bar, which good counsel raises, goes. Returns how many went.</summary>
        public static int Motion(JusticeCase c, float quality, List<string> dropped)
        {
            c.MotionTried = true;
            float bar = JusticeLaw.DismissBelow + 0.12f * quality;
            int count = 0;
            for (int i = 0; i < c.Charges.Count; i++)
            {
                JusticeCharge charge = c.Charges[i];
                if (!charge.Dropped && charge.Evidence < bar)
                {
                    charge.Dropped = true;
                    count++;
                    dropped?.Add(JusticeText.Charge(charge.Id));
                }
            }
            return count;
        }

        /// <summary>The plea deal for this case and counsel, changing nothing: the charges let go, and the main one's lesser form.</summary>
        public static void DealPlan(JusticeCase c, float quality, List<JusticeCharge> drop, out JusticeCharge main, out string lesser)
        {
            drop?.Clear();
            main = c.Main;
            lesser = null;
            if (main == null)
            {
                return;
            }
            float bar = 0.45f + 0.2f * quality;
            for (int i = 0; i < c.Charges.Count; i++)
            {
                JusticeCharge charge = c.Charges[i];
                if (!charge.Dropped && charge != main && charge.Evidence < bar)
                {
                    drop?.Add(charge);
                }
            }
            ChargeDef def = JusticeLaw.Find(main.Id);
            if (def != null && def.LesserId != null && quality >= 0.45f && main.Evidence < 0.9f && !c.Has(def.LesserId))
            {
                lesser = def.LesserId;
            }
        }

        /// <summary>The deal in words: what is pleaded to, what is dropped, what it does to the sentence.</summary>
        public static string DealText(JusticeCase c, float quality)
        {
            var drop = new List<JusticeCharge>(4);
            DealPlan(c, quality, drop, out JusticeCharge main, out string lesser);
            if (main == null)
            {
                return string.Empty;
            }
            string text = lesser != null
                ? JusticeText.T("reallife_justice_deal_reduced", "from", JusticeText.Charge(main.Id), "to", JusticeText.Charge(lesser))
                : JusticeText.T("reallife_justice_deal_plead", "charge", JusticeText.Charge(main.Id));
            if (drop.Count > 0)
            {
                var names = new List<string>(drop.Count);
                for (int i = 0; i < drop.Count; i++)
                {
                    names.Add(JusticeText.Charge(drop[i].Id));
                }
                text += " " + JusticeText.T("reallife_justice_deal_dropped", "charges", string.Join(", ", names));
            }
            ChargeDef after = JusticeLaw.Find(lesser ?? main.Id);
            text += " " + JusticeText.T(after != null && after.Class == ChargeClass.Grave ? "reallife_justice_deal_tail_prison" : "reallife_justice_deal_tail");
            return text;
        }
    }

    /// <summary>The Legal contact on the phone, whose conversation is also held face to face at a law firm's desk.</summary>
    public static class LegalPhone
    {
        public const string ContactId = "reallife-legal";
        private const string DialogTypeName = "reallife_legal_calldialogtype";

        public static Contact Contact { get; private set; }

        public static void Setup(IModLogger logger)
        {
            Contact = null;
            if (!JusticeSettings.Phone || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                ContactPictures.Add(ContactId);
                Contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_legal_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
                Contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new LegalDialog()));
                if (Contact.messagesQueue == null || Contact.messagesQueue.Count == 0)
                {
                    Contact.SendMessage(new TextMessage("reallife_text_legal_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                Contact = null;
                logger?.Error(e);
                logger?.Warn("Justice: the Legal contact could not be added; the Legal file opens from the options page's test button only.");
            }
        }

        /// <summary>A text from the courts or counsel in the Legal contact's thread; a toast without the contact.</summary>
        public static void Send(string key, Dictionary<string, string> data = null)
        {
            try
            {
                if (Contact != null)
                {
                    Contact.SendMessage(new TextMessage(key, data), notify: true);
                    return;
                }
            }
            catch (Exception)
            {
                // the toast below still tells them
            }
            UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, key, data, 12f);
        }

        /// <summary>The conversation face to face at a firm's desk, in the game's own dialogue panel.</summary>
        public static void OpenInPerson(LawFirm firm)
        {
            LegalDialog.AtFirm = firm;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new LegalDialog()));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }
    }

    /// <summary>The Legal contact's conversation, on the phone or at a firm's desk.</summary>
    public class LegalDialog : Dialog
    {
        /// <summary>Set just before the dialog is opened at a firm's desk; null on the phone.</summary>
        internal static LawFirm AtFirm;

        private static LawFirm _here;                                           // the firm whose office this conversation is in
        private static readonly List<LawFirm> Options = new List<LawFirm>(8);   // null entries stand for the public defender and for oneself

        public LegalDialog()
        {
            npcNameKey = LegalPhone.ContactId;
            _here = AtFirm;
            AtFirm = null;
            if (!Justice.Active)
            {
                DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_justice_call_off")));
                return;
            }
            if (_here != null)
            {
                DialogController.current.ShowEntry(Guard.Entry(AtDesk()));
                return;
            }
            DialogController.current.ShowEntry(Guard.Entry(Root()));
        }

        private static DialogEntry Raw(string text)
        {
            return new DialogEntry { messageData = "reallife_justice_raw".Localize(new { text }), OnCancel = PhoneContacts.HangUp };
        }

        private static DialogEntry Root()
        {
            DialogEntry entry = Raw(LegalFile.Brief());
            entry.ConfirmTextOverride = "reallife_justice_call_open_file".Localize();
            entry.OnConfirm = () =>
            {
                JusticeDirector.Instance?.QueueFile();
                return PhoneContacts.Closing("reallife_justice_call_file");
            };
            if (JusticeState.OpenCount > 0)
            {
                if (JusticeState.Lawyer == LawyerKind.None)
                {
                    entry.SecondOptionTextOverride = "reallife_justice_call_find";
                    entry.OnSecondOption = () => Offer(0);
                }
                else
                {
                    entry.SecondOptionTextOverride = "reallife_justice_call_counsel";
                    entry.OnSecondOption = Counsel;
                }
            }
            return entry;
        }

        private static DialogEntry AtDesk()
        {
            JusticeCase c = JusticeState.NextOpen;
            if (c == null)
            {
                return PhoneContacts.Closing("reallife_justice_desk_no_case");
            }
            bool retained = JusticeState.Lawyer == LawyerKind.Firm && _here.Street == JusticeState.LawyerStreet && _here.Number == JusticeState.LawyerNumber;
            if (!retained)
            {
                return FirmOffer(_here, null);
            }
            if (!c.MetInPerson)
            {
                c.MetInPerson = true;   // prepared across a desk: worth a little in court
                JusticeDirector.Logger?.Info("Justice: case " + c.Number + " prepared in person with " + _here.Name + ".");
            }
            return Counsel();
        }

        private static void BuildOptions()
        {
            Options.Clear();
            Options.Add(null);   // the public defender
            Options.Add(null);   // yourself
            if (JusticeSettings.Lawyers)
            {
                Options.AddRange(LegalCounsel.List());
            }
        }

        private static DialogEntry Offer(int index)
        {
            BuildOptions();
            index %= Options.Count;
            int next = (index + 1) % Options.Count;
            Func<DialogEntry> more = () => Offer(next);
            if (index == 0)
            {
                return PlainOffer(JusticeText.T("reallife_justice_offer_pd"), LawyerKind.PublicDefender, "reallife_justice_hired_pd", more);
            }
            if (index == 1)
            {
                float quality = JusticeLaw.SelfQuality(LegalCounsel.LawyerSkillValue());
                string text = JusticeText.T("reallife_justice_offer_self", "skill", JusticeText.N(Mathf.FloorToInt(LegalCounsel.LawyerSkillValue())),
                    "effect", JusticeText.Percent(JusticeLaw.CounselEffect(quality)));
                return PlainOffer(text, LawyerKind.Self, "reallife_justice_hired_self", more);
            }
            return FirmOffer(Options[index], more);
        }

        // The public defender or oneself: no fee, nothing to sign.
        private static DialogEntry PlainOffer(string text, LawyerKind kind, string hiredKey, Func<DialogEntry> more)
        {
            DialogEntry entry = Raw(text);
            entry.ConfirmTextOverride = "reallife_justice_offer_take".Localize();
            entry.OnConfirm = () =>
            {
                LegalCounsel.RetainSimple(kind, JusticeDirector.Logger);
                return PhoneContacts.Closing(hiredKey);
            };
            entry.SecondOptionTextOverride = "reallife_justice_offer_next";
            entry.OnSecondOption = more;
            return entry;
        }

        private static DialogEntry FirmOffer(LawFirm firm, Func<DialogEntry> more)
        {
            int fee = LegalCounsel.Fee(firm);
            float effect = JusticeLaw.CounselEffect(JusticeLaw.FirmQuality(firm.Stars));
            string text = firm.Own
                ? JusticeText.T("reallife_justice_offer_own", "name", firm.Name, "stars", JusticeText.Stars(firm.Stars), "effect", JusticeText.Percent(effect))
                : JusticeText.T(firm.Chambers ? "reallife_justice_offer_chambers" : "reallife_justice_offer_firm", "name", firm.Name, "address", firm.Address,
                      "stars", JusticeText.Stars(firm.Stars))
                  + " " + JusticeText.T("reallife_justice_offer_terms", "fee", JusticeText.Money(fee), "down", JusticeText.Money(JusticeLaw.Round10(fee * 0.25f)),
                      "effect", JusticeText.Percent(effect));
            DialogEntry entry = Raw(text);
            entry.ConfirmTextOverride = "reallife_justice_offer_retain".Localize();
            entry.OnConfirm = () =>
            {
                LegalCounsel.Retain(firm, JusticeDirector.Logger);
                return PhoneContacts.Closing("reallife_justice_hired_firm", new { name = firm.Name });
            };
            if (more != null)
            {
                entry.SecondOptionTextOverride = "reallife_justice_offer_next";
                entry.OnSecondOption = more;
            }
            return entry;
        }

        private static DialogEntry Counsel()
        {
            JusticeCase c = JusticeState.NextOpen;
            JusticeCharge main = c != null ? c.Main : null;
            if (main == null)
            {
                return PhoneContacts.Closing("reallife_justice_desk_no_case");
            }
            float quality = LegalCounsel.Quality(c);
            float against = JusticeLaw.CaseAgainst(main.Evidence, Justice.Priors, quality, 0) / 1000f;
            string key = against >= 0.70f ? "reallife_justice_counsel_strong" : against >= 0.45f ? "reallife_justice_counsel_even" : "reallife_justice_counsel_thin";
            string text = JusticeText.T(key, "charge", JusticeText.Charge(main.Id), "percent", JusticeText.Percent(against))
                          + " " + JusticeText.T(c.Stage == CaseStage.Warrant ? "reallife_justice_counsel_warrant" : "reallife_justice_counsel_date",
                              "day", JusticeText.N(c.CourtDay));
            if (JusticeState.Lawyer == LawyerKind.Firm && !c.MetInPerson && _here == null)
            {
                text += " " + JusticeText.T("reallife_justice_counsel_come_in");
            }
            DialogEntry entry = Raw(text);
            entry.ConfirmTextOverride = "reallife_justice_counsel_deal".Localize();
            entry.OnConfirm = Deal;
            if (!c.MotionTried)
            {
                entry.SecondOptionTextOverride = "reallife_justice_counsel_motion";
                entry.OnSecondOption = Motion;
            }
            return entry;
        }

        private static DialogEntry Deal()
        {
            JusticeCase c = JusticeState.NextOpen;
            if (c == null || c.Main == null)
            {
                return PhoneContacts.Closing("reallife_justice_desk_no_case");
            }
            c.DealReady = true;
            return PhoneContacts.Closing("reallife_justice_deal_offer", new { terms = LegalCounsel.DealText(c, LegalCounsel.Quality(c)) });
        }

        private static DialogEntry Motion()
        {
            JusticeCase c = JusticeState.NextOpen;
            if (c == null || c.Main == null)
            {
                return PhoneContacts.Closing("reallife_justice_desk_no_case");
            }
            var dropped = new List<string>(4);
            int count = LegalCounsel.Motion(c, LegalCounsel.Quality(c), dropped);
            if (count == 0)
            {
                return PhoneContacts.Closing("reallife_justice_motion_none");
            }
            if (c.Standing == 0)
            {
                JusticeDirector.Instance?.Dismiss(c, announce: false);
                return PhoneContacts.Closing("reallife_justice_motion_all");
            }
            return PhoneContacts.Closing("reallife_justice_motion_some", new { charges = string.Join(", ", dropped) });
        }
    }
}
