using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using Buildings;
using Dialogs;
using Entities;
using Extensions;
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
        public bool Chambers;   // not one of the game's law firm businesses: rooms in an office building (a city with no firm)

        public string Address => new Address(Street, Number).ToFormattedString();
    }

    /// <summary>
    /// Who can represent the player, and what they cost. The firms are the game's own law firm
    /// businesses (<c>ba:businesstype_lawfirm</c>), read from the save's building registrations
    /// when a list is asked for (a phone call, never a timer). The game keeps a satisfaction
    /// score per business but leaves it at 50 for the ones it runs itself, so a firm's rating is
    /// fixed by its address (two to five stars, the same in every session) and moved by that
    /// score where it has one: a star for every 25 points from 50. A city with no law firm at all
    /// gets three sets of chambers in its office buildings, picked and rated the same way.
    /// </summary>
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

        /// <summary>The firms there are, best first. Rebuilt on every call: asked for when a conversation opens.</summary>
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
                if (registration == null || registration.businessTypeName != LawFirmType || string.IsNullOrEmpty(registration.StreetName))
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
            int stars = 2 + JusticeLaw.Hash(registration.StreetName + "#" + registration.StreetNumber) % 4;
            if (registration.satisfaction != null)
            {
                stars += Mathf.RoundToInt((registration.satisfaction.overall - 50) / 25f);
            }
            return new LawFirm
            {
                Name = string.IsNullOrEmpty(registration.BusinessName) ? JusticeText.T("reallife_justice_firm_unnamed") : registration.BusinessName,
                Street = registration.StreetName, Number = registration.StreetNumber, Stars = Mathf.Clamp(stars, 1, 5), Own = registration.RentedByPlayer
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

        // No law firm business anywhere: three sets of chambers in office buildings, the three whose addresses hash lowest.
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
                    if (building == null || building.BuildingType != "ba:buildingtype_office" || string.IsNullOrEmpty(building.StreetName) || Taken(building))
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

        // ---------------------------------------------------------------- the lawyer retained

        /// <summary>The player's own skill in law, 0 to 100, from the game's skill system.</summary>
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

        /// <summary>How good the player's representation is, 0 to 1, for a case (preparing it face to face adds 0.05).</summary>
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

        /// <summary>What a firm asks for the player's open cases: by the gravest of them and the firm's rating, bounded to 30 % of cash (never under 200).</summary>
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
                fame = Justice.Bridges.Fame != null ? Mathf.Clamp(Justice.Bridges.Fame(), 0, 250) : 0;   // fame costs money in court: a little more, a quarter at most
            }
            catch (Exception)
            {
                fame = 0;
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

        /// <summary>True if this registration is the firm the player has retained.</summary>
        public static bool IsRetained(BuildingRegistration registration)
        {
            return registration != null && JusticeState.Lawyer == LawyerKind.Firm && registration.StreetName == JusticeState.LawyerStreet
                   && registration.StreetNumber == JusticeState.LawyerNumber;
        }

        // ---------------------------------------------------------------- what counsel can do before the hearing

        /// <summary>
        /// The one motion to dismiss a case allows: every charge whose evidence is under the bar (0.34, raised by up to
        /// 0.12 by a good lawyer) is thrown out. Returns how many went; the case may be left with none.
        /// </summary>
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

        /// <summary>
        /// What a plea deal would be for this case with counsel of this quality: which standing charges the prosecutor
        /// lets go (not the main one; evidence under 0.45 + 0.2 x quality) and what the main one comes down to (if it
        /// has a lesser form, counsel is at least 0.45 and the evidence is short of overwhelming). Changes nothing.
        /// </summary>
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

    /// <summary>
    /// The Legal contact on the phone, and the same conversation face to face at a law firm's
    /// office: where things stand, the Legal file, finding and retaining a lawyer, and what a
    /// lawyer once retained says about the case and can do about it before the hearing.
    /// Registered the way the mod's other contacts are (PhoneContacts, JobPhone).
    /// </summary>
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
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_legal_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
                Contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, () => new LegalDialog());
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

        /// <summary>A text from the courts or from counsel, in the Legal contact's thread. Without the contact it is a toast.</summary>
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

        /// <summary>The conversation face to face, in the game's own dialogue panel, at a firm's office.</summary>
        public static void OpenInPerson(LawFirm firm)
        {
            LegalDialog.AtFirm = firm;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, () => new LegalDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        // A plain coloured disc with a white middle, like the mod's other contacts; the game looks icons up by sprite name == contact id.
        private static Sprite MakeIcon()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var colour = new Color(0.62f, 0.48f, 0.18f);
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
            Sprite icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            icon.name = ContactId;
            return icon;
        }
    }

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
            JusticeDirector director = JusticeDirector.Instance;
            if (director == null || !Justice.Active)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_justice_call_off"));
                return;
            }
            if (_here != null)
            {
                DialogController.current.ShowEntry(AtDesk());
                return;
            }
            DialogController.current.ShowEntry(Root());
        }

        private static DialogEntry Raw(string text)
        {
            return new DialogEntry { messageData = "reallife_justice_raw".Localize(new { text }), OnCancel = PhoneContacts.HangUp };
        }

        // On the phone: where things stand, then the file or the lawyer.
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

        // At a firm's desk: the firm retained talks about the case; any other makes its offer.
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
                c.MetInPerson = true;   // the case is prepared across a desk: worth a little in court
                JusticeDirector.Logger?.Info("Justice: case " + c.Number + " prepared in person with " + _here.Name + ".");
            }
            return Counsel();
        }

        // ---------------------------------------------------------------- finding a lawyer

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
                DialogEntry entry = Raw(JusticeText.T("reallife_justice_offer_pd"));
                entry.ConfirmTextOverride = "reallife_justice_offer_take".Localize();
                entry.OnConfirm = () =>
                {
                    LegalCounsel.RetainSimple(LawyerKind.PublicDefender, JusticeDirector.Logger);
                    return PhoneContacts.Closing("reallife_justice_hired_pd");
                };
                entry.SecondOptionTextOverride = "reallife_justice_offer_next";
                entry.OnSecondOption = more;
                return entry;
            }
            if (index == 1)
            {
                float quality = JusticeLaw.SelfQuality(LegalCounsel.LawyerSkillValue());
                DialogEntry entry = Raw(JusticeText.T("reallife_justice_offer_self", "skill", JusticeText.N(Mathf.FloorToInt(LegalCounsel.LawyerSkillValue())),
                    "effect", JusticeText.Percent(JusticeLaw.CounselEffect(quality))));
                entry.ConfirmTextOverride = "reallife_justice_offer_take".Localize();
                entry.OnConfirm = () =>
                {
                    LegalCounsel.RetainSimple(LawyerKind.Self, JusticeDirector.Logger);
                    return PhoneContacts.Closing("reallife_justice_hired_self");
                };
                entry.SecondOptionTextOverride = "reallife_justice_offer_next";
                entry.OnSecondOption = more;
                return entry;
            }
            return FirmOffer(Options[index], more);
        }

        // A firm says what it is, what it costs and, honestly, what it is worth. Nobody claims more than their rating.
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
                JusticeDirector.Instance?.Touch();
                return PhoneContacts.Closing("reallife_justice_hired_firm", new { name = firm.Name });
            };
            if (more != null)
            {
                entry.SecondOptionTextOverride = "reallife_justice_offer_next";
                entry.OnSecondOption = more;
            }
            return entry;
        }

        // ---------------------------------------------------------------- the lawyer on the case

        // What counsel honestly makes of it: the charge that matters, how strong it is, the odds as they would be in court today.
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
                          + " " + JusticeText.T(c.Stage == CaseStage.Warrant ? "reallife_justice_counsel_warrant" : "reallife_justice_counsel_date", "day", JusticeText.N(c.CourtDay));
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
            JusticeDirector.Instance?.Touch();
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
            JusticeDirector.Instance?.Touch();
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
