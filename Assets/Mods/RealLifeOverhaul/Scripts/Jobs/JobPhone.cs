using System;
using BAModAPI;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Finding work: a "Gig Line" contact on the phone. Calling it goes through the jobs there
    /// are, one at a time, each with what it pays right now, the player's level in it, how much
    /// of it is left today and, if it cannot be taken, why not. It is also where a job in
    /// progress is given up. Registered the way the mod's other contacts are (PhoneContacts).
    /// </summary>
    public static class JobPhone
    {
        public const string ContactId = "reallife-jobs";

        private static Sprite _icon;   // kept for the session: the game caches contact icons by sprite name

        /// <summary>The jobs' badge: the contact's picture, and the icon of a job's place on the city map.</summary>
        public static Sprite Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = MakeIcon();
                }
                return _icon;
            }
        }

        public static void Setup()
        {
            if (!Settings.Jobs || !Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                AddIcon();
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_jobs_desc", null, false, true);
                var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_jobs_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, () => new JobLineDialog());
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_jobs_welcome"), notify: false);
                }
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
                JobDirector.Logger?.Warn("Jobs: the Gig Line contact could not be added; jobs can still be started from the options page.");
            }
        }

        /// <summary>
        /// The same conversation face to face, at a help-wanted board: the game's own dialogue
        /// panel, the way its counters and desks open one.
        /// </summary>
        public static void OpenInPerson()
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_jobs_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, () => new JobLineDialog());
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        // A plain coloured disc with a white middle, like the mod's other contacts; the game looks icons up by sprite name == contact id.
        private static void AddIcon()
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
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = Icon;
            references.contactIcons = icons;
        }

        private static Sprite MakeIcon()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var colour = new Color(0.20f, 0.62f, 0.95f);
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

    public class JobLineDialog : Dialog
    {
        public JobLineDialog()
        {
            npcNameKey = JobPhone.ContactId;
            JobDirector director = JobDirector.Instance;
            if (director == null || !Settings.Jobs)
            {
                DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_job_no_off"));
            }
            else if (director.HasJob)
            {
                DialogController.current.ShowEntry(OnJob(director));
            }
            else
            {
                DialogController.current.ShowEntry(Offer(0));
            }
        }

        // On a job already: what it has paid so far, and the way out of it.
        private static DialogEntry OnJob(JobDirector director)
        {
            return new DialogEntry
            {
                messageData = "reallife_jobline_on_job".Localize(new
                {
                    job = director.Current.NameKey.GetLocalization(),
                    amount = ((float)director.ShiftTotal).ToCurrencyFormat()
                }),
                ConfirmTextOverride = "reallife_jobline_quit".Localize(),
                OnConfirm = () =>
                {
                    JobDirector.Instance?.Quit();
                    return PhoneContacts.Closing("reallife_jobline_quit_done");
                },
                OnCancel = PhoneContacts.HangUp
            };
        }

        // One job: its pitch with today's figures, then "take it", "what else" or hang up.
        internal static DialogEntry Offer(int index)
        {
            JobDef[] offers = JobDirector.Offers;
            JobDef def = offers[index % offers.Length];
            int next = (index + 1) % offers.Length;
            JobDirector director = JobDirector.Instance;
            string reason = null;
            bool available = director != null && director.Available(def, out reason);
            int level = JobState.Level(def);
            float rate = JobCatalogue.PayFactor(level) * JobDirector.PayScale();   // what the work will really pay, owners' cut included
            var entry = new DialogEntry
            {
                messageData = def.PitchKey.Localize(new
                {
                    pay = (def.UnitPay * rate).ToCurrencyFormat(),
                    shift = (def.UnitPay * def.ShiftUnits * rate).ToCurrencyFormat(),
                    km = (def.PayPerMetre * 1000f * rate).ToCurrencyFormat(),
                    level = level.ToString(),
                    left = JobState.LeftToday(def).ToString(),
                    status = (available ? "reallife_jobline_ready" : reason ?? "reallife_job_no_now").GetLocalization()
                }),
                OnCancel = PhoneContacts.HangUp
            };
            if (available)
            {
                entry.ConfirmTextOverride = "reallife_jobline_take".Localize();
                entry.OnConfirm = () => Take(def);
                entry.SecondOptionTextOverride = "reallife_jobline_next";
                entry.OnSecondOption = () => Offer(next);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_jobline_next".Localize();
                entry.OnConfirm = () => Offer(next);
            }
            return entry;
        }

        private static DialogEntry Take(JobDef def)
        {
            JobDirector director = JobDirector.Instance;
            if (director == null)
            {
                return PhoneContacts.Closing("reallife_job_no_off");
            }
            // Asked again now: the day's work may have run out, or the car driven off, while the phone rang.
            return director.Take(def, out string reason)
                ? PhoneContacts.Closing("reallife_jobline_go")
                : PhoneContacts.Closing(reason ?? "reallife_jobline_failed");
        }
    }
}
