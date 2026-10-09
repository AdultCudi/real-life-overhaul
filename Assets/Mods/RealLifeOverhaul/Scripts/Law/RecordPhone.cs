using System;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The "Record" page: a Police Records Desk contact on the phone. Calling it reads out how
    /// the player stands with the police and with the public, how long ago their last offence
    /// was, and then the record itself, newest first, a line at a time. It can also point the
    /// game's destination marker at the nearest precinct. Registered the way the mod's other
    /// contacts are (Phone/PhoneContacts, Jobs/JobPhone).
    /// </summary>
    public static class RecordPhone
    {
        public const string ContactId = "reallife-records";

        private static Sprite _icon;   // kept for the session: the game caches contact icons by sprite name

        public static void Setup(Sprite badge)
        {
            if (!Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                AddIcon(badge);
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_records_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_records_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, () => new RecordsDialog());
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_records_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                LawDirector.Logger?.Error(e);
                LawDirector.Logger?.Warn("Law: the Police Records Desk contact could not be added; the record is still kept.");
            }
        }

        // A police-blue disc with the badge star on it; the game looks icons up by sprite name == contact id.
        private static void AddIcon(Sprite badge)
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == ContactId)
                {
                    return;
                }
            }
            if (_icon == null)
            {
                _icon = MakeIcon(badge);
            }
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = _icon;
            references.contactIcons = icons;
        }

        private static Sprite MakeIcon(Sprite badge)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var colour = new Color(0.10f, 0.28f, 0.78f);
            Texture2D star = badge != null ? badge.texture : null;
            float radius = size / 2f - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    Color pixel = colour;
                    if (star != null)
                    {
                        // The badge, two thirds the size of the disc, in the middle of it.
                        float u = (x - size * 0.5f) / (size * 0.66f) + 0.5f;
                        float v = (y - size * 0.5f) / (size * 0.66f) + 0.5f;
                        if (u >= 0f && u < 1f && v >= 0f && v < 1f)
                        {
                            float white = star.GetPixel((int)(u * star.width), (int)(v * star.height)).a;
                            pixel = Color.Lerp(colour, Color.white, white);
                        }
                    }
                    else if (distance < radius * 0.42f)
                    {
                        pixel = Color.white;
                    }
                    pixel.a = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = pixel;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = ContactId;
            return sprite;
        }
    }

    public class RecordsDialog : Dialog
    {
        public RecordsDialog()
        {
            npcNameKey = RecordPhone.ContactId;
            if (!LawState.Loaded)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_records_closed"));
                return;
            }
            DialogController.current.ShowEntry(Summary());
        }

        private static DialogEntry Summary()
        {
            int since = Record.DaysSinceLastOffence;
            string last = since < 0 ? "reallife_records_since_never".GetLocalization()
                : since == 0 ? "reallife_records_since_today".GetLocalization()
                : "reallife_records_since_days".GetLocalization().Replace("{days}", since.ToString());
            var entry = new DialogEntry
            {
                messageData = "reallife_records_summary".Localize(new
                {
                    police = Reputation.PoliceWord.GetLocalization(),
                    policeScore = Reputation.Police.ToString(),
                    people = Reputation.PublicWord.GetLocalization(),
                    peopleScore = Reputation.Public.ToString(),
                    count = Record.Count.ToString(),
                    since = last,
                    watch = (Reputation.NoWarnings ? "reallife_records_watch_high" : Reputation.ScrutinyLevel > 0 ? "reallife_records_watch_some" : "reallife_records_watch_none").GetLocalization()
                }),
                ConfirmTextOverride = "reallife_records_read".Localize(),
                OnConfirm = () => Record.Count > 0 ? Line(Record.Count - 1) : PhoneContacts.Closing("reallife_records_clean"),
                SecondOptionTextOverride = "reallife_records_nearest",
                OnSecondOption = Nearest,
                OnCancel = PhoneContacts.HangUp
            };
            return entry;
        }

        // One line of the record, newest first, then the one before it.
        private static DialogEntry Line(int index)
        {
            if (index < 0 || index >= Record.Count)
            {
                return PhoneContacts.Closing("reallife_records_end");
            }
            RecordEntry line = Record.Entry(index);
            string outcome;
            switch (line.Outcome)
            {
                case Outcome.Held: outcome = "reallife_records_outcome_held".GetLocalization(); break;
                case Outcome.Served: outcome = "reallife_records_outcome_served".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                case Outcome.Bailed: outcome = "reallife_records_outcome_bailed".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                case Outcome.Escaped: outcome = "reallife_records_outcome_escaped".GetLocalization(); break;
                default: outcome = "reallife_records_outcome_cited".GetLocalization(); break;
            }
            return new DialogEntry
            {
                messageData = "reallife_records_line".Localize(new
                {
                    number = (Record.Count - index).ToString(),
                    count = Record.Count.ToString(),
                    day = line.Day.ToString(),
                    charges = Custody.ChargeNames(line.Charges, ", "),
                    outcome,
                    amount = ((float)line.Fines).ToCurrencyFormat()
                }),
                ConfirmTextOverride = (index > 0 ? "reallife_records_next" : "reallife_records_done").Localize(),
                OnConfirm = () => index > 0 ? Line(index - 1) : PhoneContacts.Closing("reallife_records_end"),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Nearest()
        {
            LawDirector director = LawDirector.Instance;
            Precinct precinct = director != null && Settings.LawPrecincts ? director.GuideToNearest() : null;
            if (precinct == null)
            {
                return PhoneContacts.Closing("reallife_records_nearest_none");
            }
            Vector3 offset = precinct.Door - PlayerHelper.GetCityPosition();
            offset.y = 0f;
            return PhoneContacts.Closing("reallife_records_nearest_done", new
            {
                precinct = precinct.Name,
                metres = Mathf.RoundToInt(offset.magnitude).ToString()
            });
        }
    }
}
