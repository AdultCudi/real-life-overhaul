using System;
using System.Collections.Generic;
using Entities;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What the player can text the partner from their messages, while the social life keeps the partner (the Family contact
    /// is not there then): a button of the game's own under their last message. Only "make this our new home", while the
    /// player stands in another home of theirs, rented or owned. The button's click is the mod's, so it is never in a save:
    /// it is taken off before each save and put back a few seconds after, and a save that is read never has it.
    /// COST: on the director's one-second tick a few comparisons; a list of one when the button goes on or comes off.
    /// </summary>
    internal static class FamilySms
    {
        public const string NewHomeKey = "reallife_family_sms_new_home";
        private const float AfterSave = 5f;   // seconds after a save before the button goes back on

        private static TextMessage _on;        // the message the button is on; null while it is not on any
        private static bool _madeData;         // the message had no additional data before the button
        private static float _quietUntil;
        private static readonly UnityEngine.Events.UnityAction Click = Guard.Listener(Clicked);   // the game's button calls it: behind the guard, made once

        /// <summary>The director's tick: the button on while the player stands in another home of theirs, off otherwise.</summary>
        public static void Tick()
        {
            bool want = Time.unscaledTime >= _quietUntil && Wanted();
            if (want && _on == null)
            {
                Put();
            }
            else if (!want && _on != null)
            {
                Take();
            }
        }

        /// <summary>Before the game writes a save: the button off, so the save never has the mod's click in it.</summary>
        public static void BeforeSave()
        {
            Take();
            _quietUntil = Time.unscaledTime + AfterSave;
        }

        public static void Reset()
        {
            Take();
            _quietUntil = 0f;
        }

        // Together, living in, the social life keeping them, and the player inside another residential building of theirs.
        private static bool Wanted()
        {
            return Settings.Family && Partner.UsesSocial && Partner.Kept(out _) && FamilyState.Together && FamilyState.S.LivesIn
                   && BuildingManager.IsInsideBuilding && !FamilyPlaces.AtHome() && FamilyPhone.InOwnHome() && FamilyPhone.PartnerContact != null;
        }

        private static void Put()
        {
            TextMessage last = Last(FamilyPhone.PartnerContact);
            if (last == null)
            {
                return;   // nothing in their messages to put it under yet
            }
            _madeData = last.additionalData == null;
            if (_madeData)
            {
                last.additionalData = new AdditionalMessageData();
            }
            if (last.additionalData.contextButtonData == null)
            {
                last.additionalData.contextButtonData = new List<TextMessage.ContextButtonData>(1);
            }
            last.additionalData.contextButtonData.Add(new TextMessage.ContextButtonData(NewHomeKey, ContextButton.BackgroundColor.blue, Click));
            _on = last;
        }

        private static void Take()
        {
            TextMessage on = _on;
            _on = null;
            List<TextMessage.ContextButtonData> buttons = on?.additionalData?.contextButtonData;
            if (buttons == null)
            {
                return;
            }
            buttons.RemoveAll(b => b.key == NewHomeKey);
            if (_madeData && buttons.Count == 0 && on.additionalData.listOfLabels == null && on.additionalData.taxes == null)
            {
                on.additionalData = null;   // as it was
            }
        }

        private static TextMessage Last(Contact contact)
        {
            TextMessage last = null;
            if (contact?.messagesQueue != null)
            {
                foreach (TextMessage m in contact.messagesQueue)
                {
                    last = m;
                }
            }
            return last;
        }

        // The game has cleared the message's buttons already. The player's text, then the partner's answer.
        private static void Clicked()
        {
            FamilyDirector.Instance?.Guarded(() =>
            {
                Take();
                Contact contact = FamilyPhone.PartnerContact;
                if (!Wanted() || contact == null)
                {
                    return;   // they walked out before the click
                }
                contact.SendMessage(new TextMessage("reallife_family_sms_new_home_sent") { isFromPlayer = true }, notify: false);
                BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
                if (!FamilyPlaces.HasRoom(here, 0))
                {
                    FamilyText.Message(contact, "reallife_family_sms_new_home_small", Args.Us);
                    return;
                }
                FamilyPlaces.MakeHome(here);
                FamilyText.Message(contact, "reallife_family_sms_new_home_yes", Args.Us);
                FamilyDirector.Logger?.Info("Family: the player texted " + Partner.Name + " to make this home theirs; it is the family home now.");
            });
        }
    }
}
